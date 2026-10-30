using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterTab.DAL.EF.Migrations
{
    /// <inheritdoc />
    public partial class PermanentAccountsAndEventPaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedLoginCount",
                table: "UserIdentities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockedUntilUtc",
                table: "UserIdentities",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Challenge = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    State = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ReturnOrigin = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountActions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountActions_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountActions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_CreatedAtUtc_Id",
                table: "Events",
                columns: new[] { "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountActions_CreatedByUserId",
                table: "AccountActions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountActions_ModifiedByUserId",
                table: "AccountActions",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountActions_TokenHash",
                table: "AccountActions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountActions_UserId",
                table: "AccountActions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountActions");

            migrationBuilder.DropIndex(
                name: "IX_Events_CreatedAtUtc_Id",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "FailedLoginCount",
                table: "UserIdentities");

            migrationBuilder.DropColumn(
                name: "LockedUntilUtc",
                table: "UserIdentities");
        }
    }
}
