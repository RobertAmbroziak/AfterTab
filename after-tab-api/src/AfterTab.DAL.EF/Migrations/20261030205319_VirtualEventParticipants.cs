using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterTab.DAL.EF.Migrations
{
    /// <inheritdoc />
    public partial class VirtualEventParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransferConfirmations_Users_FromUserId",
                table: "TransferConfirmations");

            migrationBuilder.DropForeignKey(
                name: "FK_TransferConfirmations_Users_ToUserId",
                table: "TransferConfirmations");

            migrationBuilder.DropIndex(
                name: "IX_TransferConfirmations_FromUserId",
                table: "TransferConfirmations");

            migrationBuilder.DropIndex(
                name: "IX_TransferConfirmations_ToUserId",
                table: "TransferConfirmations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TransferConfirmations_Valid",
                table: "TransferConfirmations");

            migrationBuilder.DropIndex(
                name: "IX_EventParticipants_EventId_UserId",
                table: "EventParticipants");

            migrationBuilder.RenameColumn(
                name: "ToUserId",
                table: "TransferConfirmations",
                newName: "ToParticipantId");

            migrationBuilder.RenameColumn(
                name: "FromUserId",
                table: "TransferConfirmations",
                newName: "FromParticipantId");

            migrationBuilder.RenameIndex(
                name: "IX_TransferConfirmations_EventId_SettlementVersion_FromUserId_ToUserId",
                table: "TransferConfirmations",
                newName: "IX_TransferConfirmations_EventId_SettlementVersion_FromParticipantId_ToParticipantId");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "EventParticipants",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "EventParticipants",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "MergedIntoParticipantId",
                table: "EventParticipants",
                type: "uniqueidentifier",
                nullable: true);

            // Preserve existing names, payment flags and transfer identities. The old
            // columns contained account IDs; the renamed columns must contain member IDs.
            migrationBuilder.Sql("""
                UPDATE p SET p.DisplayName = u.DisplayName
                FROM EventParticipants p INNER JOIN Users u ON u.Id = p.UserId;
                UPDATE c SET c.FromParticipantId = f.Id, c.ToParticipantId = t.Id
                FROM TransferConfirmations c
                INNER JOIN EventParticipants f ON f.EventId = c.EventId AND f.UserId = c.FromParticipantId
                INNER JOIN EventParticipants t ON t.EventId = c.EventId AND t.UserId = c.ToParticipantId;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TransferConfirmations_FromParticipantId_EventId",
                table: "TransferConfirmations",
                columns: new[] { "FromParticipantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferConfirmations_ToParticipantId_EventId",
                table: "TransferConfirmations",
                columns: new[] { "ToParticipantId", "EventId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TransferConfirmations_Valid",
                table: "TransferConfirmations",
                sql: "[AmountMinor] > 0 AND [SettlementVersion] > 0 AND [FromParticipantId] <> [ToParticipantId]");

            migrationBuilder.CreateIndex(
                name: "IX_EventParticipants_EventId_UserId",
                table: "EventParticipants",
                columns: new[] { "EventId", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EventParticipants_MergedIntoParticipantId_EventId",
                table: "EventParticipants",
                columns: new[] { "MergedIntoParticipantId", "EventId" });

            migrationBuilder.AddForeignKey(
                name: "FK_EventParticipants_EventParticipants_MergedIntoParticipantId_EventId",
                table: "EventParticipants",
                columns: new[] { "MergedIntoParticipantId", "EventId" },
                principalTable: "EventParticipants",
                principalColumns: new[] { "Id", "EventId" });

            migrationBuilder.AddForeignKey(
                name: "FK_TransferConfirmations_EventParticipants_FromParticipantId_EventId",
                table: "TransferConfirmations",
                columns: new[] { "FromParticipantId", "EventId" },
                principalTable: "EventParticipants",
                principalColumns: new[] { "Id", "EventId" });

            migrationBuilder.AddForeignKey(
                name: "FK_TransferConfirmations_EventParticipants_ToParticipantId_EventId",
                table: "TransferConfirmations",
                columns: new[] { "ToParticipantId", "EventId" },
                principalTable: "EventParticipants",
                principalColumns: new[] { "Id", "EventId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rolling back would discard people without accounts or merged aliases.
            // Refuse rather than delete their financial data or create fictional users.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM EventParticipants WHERE UserId IS NULL)
                    THROW 51000, 'Cannot roll back while virtual or merged participants exist.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_EventParticipants_EventParticipants_MergedIntoParticipantId_EventId",
                table: "EventParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_TransferConfirmations_EventParticipants_FromParticipantId_EventId",
                table: "TransferConfirmations");

            migrationBuilder.DropForeignKey(
                name: "FK_TransferConfirmations_EventParticipants_ToParticipantId_EventId",
                table: "TransferConfirmations");

            migrationBuilder.DropIndex(
                name: "IX_TransferConfirmations_FromParticipantId_EventId",
                table: "TransferConfirmations");

            migrationBuilder.DropIndex(
                name: "IX_TransferConfirmations_ToParticipantId_EventId",
                table: "TransferConfirmations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TransferConfirmations_Valid",
                table: "TransferConfirmations");

            migrationBuilder.DropIndex(
                name: "IX_EventParticipants_EventId_UserId",
                table: "EventParticipants");

            migrationBuilder.DropIndex(
                name: "IX_EventParticipants_MergedIntoParticipantId_EventId",
                table: "EventParticipants");

            migrationBuilder.Sql("""
                UPDATE c SET c.FromParticipantId = f.UserId, c.ToParticipantId = t.UserId
                FROM TransferConfirmations c
                INNER JOIN EventParticipants f ON f.EventId = c.EventId AND f.Id = c.FromParticipantId
                INNER JOIN EventParticipants t ON t.EventId = c.EventId AND t.Id = c.ToParticipantId;
                """);

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "EventParticipants");

            migrationBuilder.DropColumn(
                name: "MergedIntoParticipantId",
                table: "EventParticipants");

            migrationBuilder.RenameColumn(
                name: "ToParticipantId",
                table: "TransferConfirmations",
                newName: "ToUserId");

            migrationBuilder.RenameColumn(
                name: "FromParticipantId",
                table: "TransferConfirmations",
                newName: "FromUserId");

            migrationBuilder.RenameIndex(
                name: "IX_TransferConfirmations_EventId_SettlementVersion_FromParticipantId_ToParticipantId",
                table: "TransferConfirmations",
                newName: "IX_TransferConfirmations_EventId_SettlementVersion_FromUserId_ToUserId");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "EventParticipants",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferConfirmations_FromUserId",
                table: "TransferConfirmations",
                column: "FromUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferConfirmations_ToUserId",
                table: "TransferConfirmations",
                column: "ToUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TransferConfirmations_Valid",
                table: "TransferConfirmations",
                sql: "[AmountMinor] > 0 AND [SettlementVersion] > 0 AND [FromUserId] <> [ToUserId]");

            migrationBuilder.CreateIndex(
                name: "IX_EventParticipants_EventId_UserId",
                table: "EventParticipants",
                columns: new[] { "EventId", "UserId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TransferConfirmations_Users_FromUserId",
                table: "TransferConfirmations",
                column: "FromUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TransferConfirmations_Users_ToUserId",
                table: "TransferConfirmations",
                column: "ToUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
