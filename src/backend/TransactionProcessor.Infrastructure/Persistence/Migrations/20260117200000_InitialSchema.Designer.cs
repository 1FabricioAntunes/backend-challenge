using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using TransactionProcessor.Infrastructure.Persistence;

#nullable disable

namespace TransactionProcessor.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260117200000_InitialSchema")]
    partial class InitialSchema
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.11")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("TransactionProcessor.Domain.Entities.FileEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("ErrorMessage")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)");

                    b.Property<string>("FileName")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<long>("FileSize")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("ProcessedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("S3Key")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<string>("StatusCode")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasDefaultValue("Uploaded")
                        .HasColumnType("character varying(50)");

                    b.Property<DateTime>("UploadedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("UploadedByUserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("S3Key")
                        .IsUnique();

                    b.HasIndex("StatusCode")
                        .HasDatabaseName("idx_files_status_code");

                    b.HasIndex("UploadedAt")
                        .HasDatabaseName("idx_files_uploaded_at");

                    b.HasIndex("UploadedByUserId")
                        .HasDatabaseName("idx_files_uploaded_by_user");

                    b.ToTable("Files", (string)null);
                });

            modelBuilder.Entity("TransactionProcessor.Domain.Entities.FileStatusEntity", b =>
                {
                    b.Property<string>("StatusCode")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<bool>("IsTerminal")
                        .HasColumnName("is_terminal")
                        .HasColumnType("boolean");

                    b.HasKey("StatusCode");

                    b.HasIndex("StatusCode")
                        .HasDatabaseName("idx_file_statuses_code");

                    b.ToTable("file_statuses", (string)null);
                });

            modelBuilder.Entity("TransactionProcessor.Domain.Entities.StoreEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(19)
                        .HasColumnType("character varying(19)");

                    b.Property<string>("OwnerName")
                        .IsRequired()
                        .HasColumnName("owner_name")
                        .HasMaxLength(14)
                        .HasColumnType("character varying(14)");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex(new[] { "Name", "OwnerName" }, "idx_stores_name_owner_unique")
                        .IsUnique();

                    b.ToTable("Stores", (string)null);
                });

            modelBuilder.Entity("TransactionProcessor.Domain.Entities.TransactionEntity", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigserial")
                        .UseIdentityByDefaultColumn();

                    b.Property<decimal>("Amount")
                        .HasPrecision(18, 2)
                        .HasColumnType("numeric(18,2)");

                    b.Property<string>("CPF")
                        .IsRequired()
                        .HasMaxLength(11)
                        .HasColumnType("character varying(11)");

                    b.Property<string>("Card")
                        .IsRequired()
                        .HasMaxLength(12)
                        .HasColumnType("character varying(12)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("FileId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("StoreId")
                        .HasColumnType("uuid");

                    b.Property<DateOnly>("TransactionDate")
                        .HasColumnName("transaction_date")
                        .HasColumnType("date");

                    b.Property<string>("TransactionTypeCode")
                        .IsRequired()
                        .HasColumnName("transaction_type_code")
                        .HasMaxLength(10)
                        .HasColumnType("character varying(10)");

                    b.Property<TimeOnly>("TransactionTime")
                        .HasColumnName("transaction_time")
                        .HasColumnType("time");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("FileId")
                        .HasDatabaseName("idx_transactions_file_id");

                    b.HasIndex("StoreId")
                        .HasDatabaseName("idx_transactions_store_id");

                    b.HasIndex("TransactionDate")
                        .HasDatabaseName("idx_transactions_date");

                    b.HasIndex(new[] { "StoreId", "TransactionDate" }, "idx_transactions_store_date");

                    b.HasIndex("TransactionTypeCode")
                        .HasDatabaseName("idx_transaction_types_code");

                    b.ToTable("Transactions", (string)null);
                });

            modelBuilder.Entity("TransactionProcessor.Domain.Entities.TransactionTypeEntity", b =>
                {
                    b.Property<string>("TypeCode")
                        .HasColumnName("type_code")
                        .HasMaxLength(10)
                        .HasColumnType("character varying(10)");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("Nature")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("Sign")
                        .IsRequired()
                        .HasMaxLength(1)
                        .HasColumnType("character varying(1)");

                    b.HasKey("TypeCode");

                    b.HasIndex("TypeCode")
                        .HasDatabaseName("idx_transaction_types_code");

                    b.ToTable("transaction_types", (string)null);
                });

            modelBuilder.Entity("TransactionProcessor.Domain.Entities.FileEntity", b =>
                {
                    b.HasOne("TransactionProcessor.Domain.Entities.FileStatusEntity", "Status")
                        .WithMany()
                        .HasForeignKey("StatusCode")
                        .IsRequired()
                        .HasConstraintName("fk_files_statuses");

                    b.Navigation("Status");
                });

            modelBuilder.Entity("TransactionProcessor.Domain.Entities.TransactionEntity", b =>
                {
                    b.HasOne("TransactionProcessor.Domain.Entities.FileEntity", "File")
                        .WithMany("Transactions")
                        .HasForeignKey("FileId")
                        .IsRequired()
                        .HasConstraintName("fk_transactions_files");

                    b.HasOne("TransactionProcessor.Domain.Entities.StoreEntity", "Store")
                        .WithMany("Transactions")
                        .HasForeignKey("StoreId")
                        .IsRequired()
                        .HasConstraintName("fk_transactions_stores");

                    b.HasOne("TransactionProcessor.Domain.Entities.TransactionTypeEntity", "TransactionType")
                        .WithMany()
                        .HasForeignKey("TransactionTypeCode")
                        .IsRequired()
                        .HasConstraintName("fk_transactions_types");

                    b.Navigation("File");
                    b.Navigation("Store");
                    b.Navigation("TransactionType");
                });

            modelBuilder.Entity("TransactionProcessor.Domain.Entities.FileEntity", b =>
                {
                    b.Navigation("Transactions");
                });

            modelBuilder.Entity("TransactionProcessor.Domain.Entities.StoreEntity", b =>
                {
                    b.Navigation("Transactions");
                });
#pragma warning restore 612, 618
        }
    }
}
