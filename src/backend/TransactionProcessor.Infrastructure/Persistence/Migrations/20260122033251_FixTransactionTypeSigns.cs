using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransactionProcessor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixTransactionTypeSigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix transaction type signs according to challenge requirements:
            // Type 1: Debit, Income, +
            // Type 2: Boleto, Expense, -
            // Type 3: Financing, Expense, -
            // Type 4: Credit, Income, +
            // Type 5: Loan Receipt, Income, +
            // Type 6: Sales, Income, +
            // Type 7: TED Receipt, Income, +
            // Type 8: DOC Receipt, Income, +
            // Type 9: Rent, Expense, -
            migrationBuilder.Sql(@"
                UPDATE transaction_types SET ""Description"" = 'Debit', ""Nature"" = 'Income', ""Sign"" = '+' WHERE type_code = '1';
                UPDATE transaction_types SET ""Description"" = 'Boleto', ""Nature"" = 'Expense', ""Sign"" = '-' WHERE type_code = '2';
                UPDATE transaction_types SET ""Description"" = 'Financing', ""Nature"" = 'Expense', ""Sign"" = '-' WHERE type_code = '3';
                UPDATE transaction_types SET ""Description"" = 'Credit', ""Nature"" = 'Income', ""Sign"" = '+' WHERE type_code = '4';
                UPDATE transaction_types SET ""Description"" = 'Loan Receipt', ""Nature"" = 'Income', ""Sign"" = '+' WHERE type_code = '5';
                UPDATE transaction_types SET ""Description"" = 'Sales', ""Nature"" = 'Income', ""Sign"" = '+' WHERE type_code = '6';
                UPDATE transaction_types SET ""Description"" = 'TED Receipt', ""Nature"" = 'Income', ""Sign"" = '+' WHERE type_code = '7';
                UPDATE transaction_types SET ""Description"" = 'DOC Receipt', ""Nature"" = 'Income', ""Sign"" = '+' WHERE type_code = '8';
                UPDATE transaction_types SET ""Description"" = 'Rent', ""Nature"" = 'Expense', ""Sign"" = '-' WHERE type_code = '9';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to old (incorrect) values
            migrationBuilder.Sql(@"
                UPDATE transaction_types SET ""Description"" = 'Débito', ""Nature"" = 'Expense', ""Sign"" = '-' WHERE type_code = '1';
                UPDATE transaction_types SET ""Description"" = 'Boleto', ""Nature"" = 'Income', ""Sign"" = '+' WHERE type_code = '2';
                UPDATE transaction_types SET ""Description"" = 'Financiamento', ""Nature"" = 'Income', ""Sign"" = '+' WHERE type_code = '3';
                UPDATE transaction_types SET ""Description"" = 'Crédito', ""Nature"" = 'Expense', ""Sign"" = '-' WHERE type_code = '4';
                UPDATE transaction_types SET ""Description"" = 'Recebimento Empr.', ""Nature"" = 'Expense', ""Sign"" = '-' WHERE type_code = '5';
                UPDATE transaction_types SET ""Description"" = 'Vendas', ""Nature"" = 'Expense', ""Sign"" = '-' WHERE type_code = '6';
                UPDATE transaction_types SET ""Description"" = 'Recebimento TED', ""Nature"" = 'Expense', ""Sign"" = '-' WHERE type_code = '7';
                UPDATE transaction_types SET ""Description"" = 'Recebimento DOC', ""Nature"" = 'Expense', ""Sign"" = '-' WHERE type_code = '8';
                UPDATE transaction_types SET ""Description"" = 'Aluguel', ""Nature"" = 'Income', ""Sign"" = '+' WHERE type_code = '9';
            ");
        }
    }
}
