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
    /// Stores that receive transactions
    /// </summary>
    public DbSet<Store> Stores { get; set; } = null!;

    /// <summary>
    /// Transactions parsed from CNAB files
    /// </summary>
    public DbSet<Transaction> Transactions { get; set; } = null!;

    /// <summary>
    /// Transaction type lookup table (business logic source of truth)
    /// Maps TypeCode to Sign (+/-), Nature, Description
    /// Code queries this table instead of hardcoding type logic
    /// </summary>
    public DbSet<TransactionType> TransactionTypes { get; set; } = null!;

    /// <summary>
    /// File status lookup table (business logic source of truth)
    /// Maps StatusCode to Description, IsTerminal flag
    /// Code queries this table instead of hardcoding status logic
    /// </summary>
    public DbSet<FileStatus> FileStatuses { get; set; } = null!;

    /// <summary>
    /// Configure entity models and relationships for normalized schema
    /// Aligns with docs/database.md - 3NF normalization with lookup tables
    /// </summary>
    /// <param name="modelBuilder">Model builder for entity configuration</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // File entity configuration (normalized schema)
        modelBuilder.Entity<FileEntity>(entity =>
        {
            entity.ToTable("Files");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.StatusCode)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Uploaded");
            entity.Property(e => e.FileSize)
                .IsRequired()
                .HasColumnType("bigint");
            entity.Property(e => e.S3Key)
                .IsRequired()
                .HasMaxLength(500);
            entity.Property(e => e.UploadedByUserId)
                .IsRequired(false);
            entity.Property(e => e.UploadedAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone");
            entity.Property(e => e.ProcessedAt)
                .HasColumnType("timestamp with time zone");
            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(1000);

            // Unique constraint on S3 key
            entity.HasIndex(e => e.S3Key)
                .IsUnique();

            // Indexes for common queries
            entity.HasIndex(e => e.StatusCode).HasDatabaseName("idx_files_status_code");
            entity.HasIndex(e => e.UploadedAt).HasDatabaseName("idx_files_uploaded_at");
            entity.HasIndex(e => e.UploadedByUserId).HasDatabaseName("idx_files_uploaded_by_user");
        });

        // Transaction entity configuration (BIGSERIAL ID, date/time split, FK to lookup)
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("Transactions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("bigserial");
            entity.Property(e => e.FileId)
                .IsRequired();
            entity.Property(e => e.StoreId)
                .IsRequired();
            entity.Property(e => e.TransactionTypeCode)
                .IsRequired()
                .HasMaxLength(10)
                .HasColumnName("transaction_type_code");
            entity.Property(e => e.Amount)
                .IsRequired()
                .HasPrecision(18, 2);
            entity.Property(e => e.TransactionDate)
                .IsRequired()
                .HasColumnType("date")
                .HasColumnName("transaction_date");
            entity.Property(e => e.TransactionTime)
                .IsRequired()
                .HasColumnType("time")
                .HasColumnName("transaction_time");
            entity.Property(e => e.CPF)
                .IsRequired()
                .HasMaxLength(11);
            entity.Property(e => e.Card)
                .IsRequired()
                .HasMaxLength(12);
            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone");
            entity.Property(e => e.UpdatedAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            // Foreign Key: Transaction belongs to File (cascading delete)
            entity.HasOne(e => e.File)
                .WithMany(f => f.Transactions)
                .HasForeignKey(e => e.FileId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_transactions_files");

            // Foreign Key: Transaction belongs to Store (restrict delete)
            entity.HasOne(e => e.Store)
                .WithMany(s => s.Transactions)
                .HasForeignKey(e => e.StoreId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_transactions_stores");

            // Indexes for query performance (aligned with docs/database.md)
            entity.HasIndex(e => e.FileId).HasDatabaseName("idx_transactions_file_id");
            entity.HasIndex(e => e.StoreId).HasDatabaseName("idx_transactions_store_id");
            entity.HasIndex(e => e.TransactionDate).HasDatabaseName("idx_transactions_date");
            entity.HasIndex(e => new { e.StoreId, e.TransactionDate })
                .HasDatabaseName("idx_transactions_store_date");
        });

        // TransactionType lookup table configuration (database-driven business logic)
        modelBuilder.Entity<TransactionType>(entity =>
        {
            entity.ToTable("transaction_types");
            entity.HasKey(e => e.TypeCode);
            entity.Property(e => e.TypeCode)
                .HasMaxLength(10)
                .HasColumnName("type_code");
            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.Nature)
                .IsRequired()
                .HasMaxLength(50)
                .HasComment("Income or Expense");
            entity.Property(e => e.Sign)
                .IsRequired()
                .HasMaxLength(1)
                .HasComment("+ for income, - for expense; source of truth for sign determination");
        });

        // FileStatus lookup table configuration (database-driven business logic)
        modelBuilder.Entity<FileStatus>(entity =>
        {
            entity.ToTable("file_statuses");
            entity.HasKey(e => e.StatusCode);
            entity.Property(e => e.StatusCode)
                .HasMaxLength(50)
                .HasColumnName("status_code");
            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.IsTerminal)
                .IsRequired()
                .HasColumnName("is_terminal")
                .HasComment("true if Processed or Rejected (terminal state)");
        });

        // Store entity configuration (composite unique key, no persisted balance)
        modelBuilder.Entity<Store>(entity =>
        {
            entity.ToTable("Stores");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(19);
            entity.Property(e => e.OwnerName)
                .IsRequired()
                .HasMaxLength(14)
                .HasColumnName("owner_name");
            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone");
            entity.Property(e => e.UpdatedAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            // Composite unique constraint (name, owner_name) - normalized key
            entity.HasIndex(e => new { e.Name, e.OwnerName })
                .IsUnique()
                .HasDatabaseName("idx_stores_name_owner_unique");

            // Note: Balance is NOT persisted; computed on-demand from transactions
            // See GetSignedAmount() method in Transaction entity and docs/database.md
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
