using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TransactionProcessor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUuidV7Function : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enable pgcrypto extension for cryptographic functions
            migrationBuilder.Sql(@"CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            // Create UUID v7 generation function
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

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Files_FileId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Stores_StoreId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_OccurredAt",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_StoreId_OccurredAt",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Stores_Code",
                table: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Stores_Name",
                table: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Files_Status",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "OccurredAtTime",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Balance",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Files");

            migrationBuilder.RenameColumn(
                name: "OccurredAt",
                table: "Transactions",
                newName: "UpdatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_Transactions_StoreId",
                table: "Transactions",
                newName: "idx_transactions_store_id");

            migrationBuilder.RenameIndex(
                name: "IX_Transactions_FileId",
                table: "Transactions",
                newName: "idx_transactions_file_id");

            migrationBuilder.RenameColumn(
                name: "Code",
                table: "Stores",
                newName: "owner_name");

            migrationBuilder.RenameIndex(
                name: "IX_Files_UploadedAt",
                table: "Files",
                newName: "idx_files_uploaded_at");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "Transactions",
                type: "bigserial",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<DateOnly>(
                name: "transaction_date",
                table: "Transactions",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "transaction_time",
                table: "Transactions",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "transaction_type_code",
                table: "Transactions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "Files",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "S3Key",
                table: "Files",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StatusCode",
                table: "Files",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Uploaded");

            migrationBuilder.AddColumn<Guid>(
                name: "UploadedByUserId",
                table: "Files",
                type: "uuid",
                nullable: true);

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

            // Seed file_statuses lookup table
            migrationBuilder.InsertData(
                table: "file_statuses",
                columns: new[] { "status_code", "Description", "is_terminal" },
                values: new object[,]
                {
                    { "Uploaded", "File uploaded and queued for processing", false },
                    { "Processing", "File is being processed", false },
                    { "Processed", "File processed successfully", true },
                    { "Rejected", "File rejected due to validation errors", true }
                });

            // Seed transaction_types lookup table (CNAB 240 specification)
            // Reference: docs/database.md § CNAB Transaction Types
            migrationBuilder.InsertData(
                table: "transaction_types",
                columns: new[] { "type_code", "Description", "Nature", "Sign" },
                values: new object[,]
                {
                    { "1", "Débito", "Expense", "-" },
                    { "2", "Boleto", "Income", "+" },
                    { "3", "Depósito", "Income", "+" },
                    { "4", "Aluguel", "Expense", "-" },
                    { "5", "Empréstimo", "Expense", "-" },
                    { "6", "Vendas", "Expense", "-" },
                    { "7", "TED", "Expense", "-" },
                    { "8", "DOC", "Expense", "-" },
                    { "9", "Crédito", "Income", "+" }
                });

            migrationBuilder.CreateIndex(
                name: "idx_transactions_date",
                table: "Transactions",
                column: "transaction_date");

            migrationBuilder.CreateIndex(
                name: "idx_transactions_store_date",
                table: "Transactions",
                columns: new[] { "StoreId", "transaction_date" });

            migrationBuilder.CreateIndex(
                name: "idx_stores_name_owner_unique",
                table: "Stores",
                columns: new[] { "Name", "owner_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_files_status_code",
                table: "Files",
                column: "StatusCode");

            migrationBuilder.CreateIndex(
                name: "idx_files_uploaded_by_user",
                table: "Files",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_S3Key",
                table: "Files",
                column: "S3Key",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_files",
                table: "Transactions",
                column: "FileId",
                principalTable: "Files",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_stores",
                table: "Transactions",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Add foreign keys for lookup tables
            migrationBuilder.AddForeignKey(
                name: "fk_transactions_transaction_types",
                table: "Transactions",
                column: "transaction_type_code",
                principalTable: "transaction_types",
                principalColumn: "type_code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_files_file_statuses",
                table: "Files",
                column: "StatusCode",
                principalTable: "file_statuses",
                principalColumn: "status_code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the UUID v7 function
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS gen_random_uuid_v7() CASCADE;");

            migrationBuilder.DropForeignKey(
                name: "fk_transactions_files",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "fk_transactions_stores",
                table: "Transactions");

            // Drop foreign keys for lookup tables
            migrationBuilder.DropForeignKey(
                name: "fk_transactions_transaction_types",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "fk_files_file_statuses",
                table: "Files");

            migrationBuilder.DropTable(
                name: "file_statuses");

            migrationBuilder.DropTable(
                name: "transaction_types");

            migrationBuilder.DropIndex(
                name: "idx_transactions_date",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "idx_transactions_store_date",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "idx_stores_name_owner_unique",
                table: "Stores");

            migrationBuilder.DropIndex(
                name: "idx_files_status_code",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "idx_files_uploaded_by_user",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "IX_Files_S3Key",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "transaction_date",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "transaction_time",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "transaction_type_code",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "S3Key",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "StatusCode",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "UploadedByUserId",
                table: "Files");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Transactions",
                newName: "OccurredAt");

            migrationBuilder.RenameIndex(
                name: "idx_transactions_store_id",
                table: "Transactions",
                newName: "IX_Transactions_StoreId");

            migrationBuilder.RenameIndex(
                name: "idx_transactions_file_id",
                table: "Transactions",
                newName: "IX_Transactions_FileId");

            migrationBuilder.RenameColumn(
                name: "owner_name",
                table: "Stores",
                newName: "Code");

            migrationBuilder.RenameIndex(
                name: "idx_files_uploaded_at",
                table: "Files",
                newName: "IX_Files_UploadedAt");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "Transactions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigserial")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "OccurredAtTime",
                table: "Transactions",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Balance",
                table: "Stores",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Files",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_OccurredAt",
                table: "Transactions",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_StoreId_OccurredAt",
                table: "Transactions",
                columns: new[] { "StoreId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Stores_Code",
                table: "Stores",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stores_Name",
                table: "Stores",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Files_Status",
                table: "Files",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Files_FileId",
                table: "Transactions",
                column: "FileId",
                principalTable: "Files",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Stores_StoreId",
                table: "Transactions",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
