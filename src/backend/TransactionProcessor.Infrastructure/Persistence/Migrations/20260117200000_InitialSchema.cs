using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TransactionProcessor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enable pgcrypto extension for UUID v7 function
            migrationBuilder.Sql(@"CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            // Create UUID v7 generation function for time-ordered UUIDs
            // UUID v7: First 48 bits = timestamp (milliseconds), remaining bits = random
            // Benefits: Time-ordered for better B-tree performance, reduces index fragmentation
            // Reference: docs/database.md § UUID v7 Configuration
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION gen_random_uuid_v7() RETURNS uuid AS $$
DECLARE
    v_time BIGINT;
    v_random BYTEA;
    v_bytes BYTEA;
BEGIN
    -- Get current timestamp in milliseconds
    v_time := (EXTRACT(epoch FROM NOW()) * 1000)::BIGINT;
    
    -- Generate random bytes (80 bits for randomness)
    v_random := gen_random_bytes(10);
    
    -- Initialize v_bytes with random data
    v_bytes := v_random;
    
    -- Set version (4 bits = 0111 for v7) at byte 6, bits 4-7
    v_bytes := set_byte(v_bytes, 6, (get_byte(v_bytes, 6) & 0x0f) | 0x70);
    
    -- Set variant (2 bits = 10) at byte 8, bits 6-7
    v_bytes := set_byte(v_bytes, 8, (get_byte(v_bytes, 8) & 0x3f) | 0x80);
    
    -- Pack timestamp into first 6 bytes (48 bits)
    v_bytes := set_byte(v_bytes, 0, (v_time >> 40)::int & 0xFF);
    v_bytes := set_byte(v_bytes, 1, (v_time >> 32)::int & 0xFF);
    v_bytes := set_byte(v_bytes, 2, (v_time >> 24)::int & 0xFF);
    v_bytes := set_byte(v_bytes, 3, (v_time >> 16)::int & 0xFF);
    v_bytes := set_byte(v_bytes, 4, (v_time >> 8)::int & 0xFF);
    v_bytes := set_byte(v_bytes, 5, v_time & 0xFF);
    
    -- Convert bytes to UUID
    RETURN encode(v_bytes, 'hex')::uuid;
END;
$$ LANGUAGE plpgsql IMMUTABLE PARALLEL SAFE;
");

            // Create lookup table for transaction types
            migrationBuilder.CreateTable(
                name: "transaction_types",
                columns: table => new
                {
                    type_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Nature = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Income or Expense"),
                    Sign = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false, comment: "+ for income, - for expense; source of truth for sign determination")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_types", x => x.type_code);
                });

            // Create lookup table for file statuses
            migrationBuilder.CreateTable(
                name: "file_statuses",
                columns: table => new
                {
                    status_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_terminal = table.Column<bool>(type: "boolean", nullable: false, comment: "true if Processed or Rejected (terminal state)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_statuses", x => x.status_code);
                });

            // Create Files table
            migrationBuilder.CreateTable(
                name: "Files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StatusCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Uploaded"),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    S3Key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Files", x => x.Id);
                    table.ForeignKey(
                        name: "fk_files_statuses",
                        column: x => x.StatusCode,
                        principalTable: "file_statuses",
                        principalColumn: "status_code",
                        onDelete: ReferentialAction.Restrict);
                });

            // Create Stores table
            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(19)", maxLength: 19, nullable: false),
                    owner_name = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                });

            // Create Transactions table
            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigserial", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_type_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    transaction_date = table.Column<DateOnly>(type: "date", nullable: false),
                    transaction_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    CPF = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    Card = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "fk_transactions_files",
                        column: x => x.FileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_transactions_stores",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transactions_types",
                        column: x => x.transaction_type_code,
                        principalTable: "transaction_types",
                        principalColumn: "type_code",
                        onDelete: ReferentialAction.Restrict);
                });

            // Create indexes for Files
            migrationBuilder.CreateIndex(
                name: "idx_files_status_code",
                table: "Files",
                column: "StatusCode");

            migrationBuilder.CreateIndex(
                name: "idx_files_uploaded_at",
                table: "Files",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "idx_files_uploaded_by_user",
                table: "Files",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_S3Key",
                table: "Files",
                column: "S3Key",
                unique: true);

            // Create indexes for Stores
            migrationBuilder.CreateIndex(
                name: "idx_stores_name_owner_unique",
                table: "Stores",
                columns: new[] { "Name", "owner_name" },
                unique: true);

            // Create indexes for Transactions
            migrationBuilder.CreateIndex(
                name: "idx_transactions_file_id",
                table: "Transactions",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "idx_transactions_store_id",
                table: "Transactions",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "idx_transactions_date",
                table: "Transactions",
                column: "transaction_date");

            migrationBuilder.CreateIndex(
                name: "idx_transactions_store_date",
                table: "Transactions",
                columns: new[] { "StoreId", "transaction_date" });

            // Seed transaction types (CNAB types 1-9)
            // According to challenge requirements:
            // Type 1: Debit, Income, +
            // Type 2: Boleto, Expense, -
            // Type 3: Financing, Expense, -
            // Type 4: Credit, Income, +
            // Type 5: Loan Receipt, Income, +
            // Type 6: Sales, Income, +
            // Type 7: TED Receipt, Income, +
            // Type 8: DOC Receipt, Income, +
            // Type 9: Rent, Expense, -
            migrationBuilder.InsertData(
                table: "transaction_types",
                columns: new[] { "type_code", "Description", "Nature", "Sign" },
                values: new object[,]
                {
                    { "1", "Debit", "Income", "+" },
                    { "2", "Boleto", "Expense", "-" },
                    { "3", "Financing", "Expense", "-" },
                    { "4", "Credit", "Income", "+" },
                    { "5", "Loan Receipt", "Income", "+" },
                    { "6", "Sales", "Income", "+" },
                    { "7", "TED Receipt", "Income", "+" },
                    { "8", "DOC Receipt", "Income", "+" },
                    { "9", "Rent", "Expense", "-" }
                });

            // Seed file statuses
            // Using raw SQL to avoid EF Core property mapping issues with snake_case column names
            migrationBuilder.Sql(@"
                INSERT INTO file_statuses (status_code, ""Description"", is_terminal) VALUES
                ('Uploaded', 'File uploaded, awaiting processing', false),
                ('Processing', 'File currently being processed', false),
                ('Processed', 'File successfully processed', true),
                ('Rejected', 'File rejected due to validation or processing errors', true)
                ON CONFLICT (status_code) DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes
            migrationBuilder.DropIndex(
                name: "idx_transactions_store_date",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "idx_transactions_date",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "idx_transactions_store_id",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "idx_transactions_file_id",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "idx_stores_name_owner_unique",
                table: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Files_S3Key",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "idx_files_uploaded_by_user",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "idx_files_uploaded_at",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "idx_files_status_code",
                table: "Files");

            // Drop tables
            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "Files");

            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropTable(
                name: "transaction_types");

            migrationBuilder.DropTable(
                name: "file_statuses");

            // Drop UUID v7 function
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS gen_random_uuid_v7() CASCADE;");
        }
    }
}
