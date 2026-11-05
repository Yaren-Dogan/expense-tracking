using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseTracker.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// AccountsAndHouseholds added expenses.ledger_id as NOT NULL with an all-zero default so existing rows could be
    /// filled, and the default stayed in the database. EF always sends the value, but any other insert that omits it
    /// would put the row in a ledger nobody can see. The model never had the default, so this is SQL only.
    /// </summary>
    public partial class ExpensesLedgerIdWithoutDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE expenses ALTER COLUMN ledger_id DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE expenses ALTER COLUMN ledger_id SET DEFAULT '00000000-0000-0000-0000-000000000000';");
        }
    }
}
