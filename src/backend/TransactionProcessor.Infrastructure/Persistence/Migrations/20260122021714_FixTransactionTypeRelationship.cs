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
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_transaction_types_TransactionTypeTypeCode",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_TransactionTypeTypeCode",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TransactionTypeTypeCode",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Balance",
                table: "Stores");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_transaction_type_code",
                table: "Transactions",
                column: "transaction_type_code");

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_types",
                table: "Transactions",
                column: "transaction_type_code",
                principalTable: "transaction_types",
                principalColumn: "type_code",
                onDelete: ReferentialAction.Restrict);
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
