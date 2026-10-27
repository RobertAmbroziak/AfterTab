using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterTab.DAL.EF.Migrations
{
    /// <inheritdoc />
    public partial class ExpenseOthers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Balanced",
                table: "Expenses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "OthersAmountMinor",
                table: "Expenses",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Expenses_Others",
                table: "Expenses",
                sql: "[OthersAmountMinor] >= 0 AND [OthersAmountMinor] <= [AmountMinor]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Expenses_Others",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "Balanced",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "OthersAmountMinor",
                table: "Expenses");
        }
    }
}
