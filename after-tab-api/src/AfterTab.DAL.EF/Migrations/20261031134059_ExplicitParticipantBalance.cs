using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterTab.DAL.EF.Migrations
{
    /// <inheritdoc />
    public partial class ExplicitParticipantBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BalanceAmountMinor",
                table: "Expenses",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "BalanceParticipantId",
                table: "Expenses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_BalanceParticipantId_EventId",
                table: "Expenses",
                columns: new[] { "BalanceParticipantId", "EventId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Expenses_Balance",
                table: "Expenses",
                sql: "[BalanceAmountMinor] >= 0 AND [BalanceAmountMinor] <= [AmountMinor] AND (([BalanceParticipantId] IS NULL AND [BalanceAmountMinor] = 0) OR ([BalanceParticipantId] IS NOT NULL AND [BalanceAmountMinor] > 0 AND [Balanced] = 1 AND [OthersAmountMinor] = 0))");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_EventParticipants_BalanceParticipantId_EventId",
                table: "Expenses",
                columns: new[] { "BalanceParticipantId", "EventId" },
                principalTable: "EventParticipants",
                principalColumns: new[] { "Id", "EventId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM Expenses WHERE BalanceParticipantId IS NOT NULL)
                    THROW 51001, 'Clear explicit balance adjustments before rolling back this migration.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_EventParticipants_BalanceParticipantId_EventId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_BalanceParticipantId_EventId",
                table: "Expenses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Expenses_Balance",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "BalanceAmountMinor",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "BalanceParticipantId",
                table: "Expenses");
        }
    }
}
