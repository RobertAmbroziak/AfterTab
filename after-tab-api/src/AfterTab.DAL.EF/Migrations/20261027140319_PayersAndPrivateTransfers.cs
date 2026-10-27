using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterTab.DAL.EF.Migrations
{
    /// <inheritdoc />
    public partial class PayersAndPrivateTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AuthorUserId",
                table: "Expenses",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("""
                UPDATE e SET AuthorUserId = COALESCE(e.CreatedByUserId, p.UserId)
                FROM Expenses e INNER JOIN EventParticipants p ON p.Id = e.PaidByParticipantId;
                """);

            migrationBuilder.CreateTable(
                name: "TransferConfirmations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SettlementVersion = table.Column<int>(type: "int", nullable: false),
                    FromUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferConfirmations", x => x.Id);
                    table.CheckConstraint("CK_TransferConfirmations_Valid", "[AmountMinor] > 0 AND [SettlementVersion] > 0 AND [FromUserId] <> [ToUserId]");
                    table.ForeignKey(
                        name: "FK_TransferConfirmations_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TransferConfirmations_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TransferConfirmations_Users_FromUserId",
                        column: x => x.FromUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TransferConfirmations_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TransferConfirmations_Users_ToUserId",
                        column: x => x.ToUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_AuthorUserId",
                table: "Expenses",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferConfirmations_CreatedByUserId",
                table: "TransferConfirmations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferConfirmations_EventId_SettlementVersion_FromUserId_ToUserId",
                table: "TransferConfirmations",
                columns: new[] { "EventId", "SettlementVersion", "FromUserId", "ToUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferConfirmations_FromUserId",
                table: "TransferConfirmations",
                column: "FromUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferConfirmations_ModifiedByUserId",
                table: "TransferConfirmations",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferConfirmations_ToUserId",
                table: "TransferConfirmations",
                column: "ToUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Users_AuthorUserId",
                table: "Expenses",
                column: "AuthorUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Users_AuthorUserId",
                table: "Expenses");

            migrationBuilder.DropTable(
                name: "TransferConfirmations");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_AuthorUserId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "AuthorUserId",
                table: "Expenses");
        }
    }
}
