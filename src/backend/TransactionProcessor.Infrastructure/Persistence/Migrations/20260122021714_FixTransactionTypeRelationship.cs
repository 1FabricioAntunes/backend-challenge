using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransactionProcessor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixTransactionTypeRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Make migration idempotent: only drop if constraint exists
            // This handles both fresh databases (where constraint doesn't exist) and existing databases
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    -- Drop foreign key if it exists
                    IF EXISTS (
                        SELECT 1 FROM pg_constraint 
                        WHERE conname = 'FK_Transactions_transaction_types_TransactionTypeTypeCode'
                    ) THEN
                        ALTER TABLE ""Transactions"" 
                        DROP CONSTRAINT ""FK_Transactions_transaction_types_TransactionTypeTypeCode"";
                    END IF;

                    -- Drop index if it exists
                    IF EXISTS (
                        SELECT 1 FROM pg_indexes 
                        WHERE indexname = 'IX_Transactions_TransactionTypeTypeCode'
                    ) THEN
                        DROP INDEX IF EXISTS ""IX_Transactions_TransactionTypeTypeCode"";
                    END IF;

                    -- Drop column if it exists
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'Transactions' 
                        AND column_name = 'TransactionTypeTypeCode'
                    ) THEN
                        ALTER TABLE ""Transactions"" 
                        DROP COLUMN ""TransactionTypeTypeCode"";
                    END IF;

                    -- Drop Balance column from Stores if it exists
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'Stores' 
                        AND column_name = 'Balance'
                    ) THEN
                        ALTER TABLE ""Stores"" 
                        DROP COLUMN ""Balance"";
                    END IF;
                END $$;
            ");

            // Create index if it doesn't exist
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Transactions_transaction_type_code"" 
                ON ""Transactions"" (""transaction_type_code"");
            ");

            // Add foreign key if it doesn't exist
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint 
                        WHERE conname = 'fk_transactions_types'
                    ) THEN
                        ALTER TABLE ""Transactions""
                        ADD CONSTRAINT ""fk_transactions_types""
                        FOREIGN KEY (""transaction_type_code"")
                        REFERENCES ""transaction_types"" (""type_code"")
                        ON DELETE RESTRICT;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transactions_types",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_transaction_type_code",
                table: "Transactions");

            migrationBuilder.AddColumn<string>(
                name: "TransactionTypeTypeCode",
                table: "Transactions",
                type: "character varying(10)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Balance",
                table: "Stores",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransactionTypeTypeCode",
                table: "Transactions",
                column: "TransactionTypeTypeCode");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_transaction_types_TransactionTypeTypeCode",
                table: "Transactions",
                column: "TransactionTypeTypeCode",
                principalTable: "transaction_types",
                principalColumn: "type_code");
        }
    }
}
