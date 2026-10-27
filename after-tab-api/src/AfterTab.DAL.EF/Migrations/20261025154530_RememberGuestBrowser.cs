using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterTab.DAL.EF.Migrations
{
    /// <inheritdoc />
    public partial class RememberGuestBrowser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GuestDeviceId",
                table: "Sessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GuestDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestDevices_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuestDevices_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuestDevices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GuestLoginRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Challenge = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReturnOrigin = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    LegacyUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LegacySessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GuestDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CodeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_GuestLoginRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestLoginRequests_GuestDevices_GuestDeviceId",
                        column: x => x.GuestDeviceId,
                        principalTable: "GuestDevices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuestLoginRequests_Sessions_LegacySessionId",
                        column: x => x.LegacySessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuestLoginRequests_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuestLoginRequests_Users_LegacyUserId",
                        column: x => x.LegacyUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuestLoginRequests_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuestLoginRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_GuestDeviceId",
                table: "Sessions",
                column: "GuestDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestDevices_CreatedByUserId",
                table: "GuestDevices",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestDevices_ModifiedByUserId",
                table: "GuestDevices",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestDevices_TokenHash",
                table: "GuestDevices",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestDevices_UserId",
                table: "GuestDevices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestLoginRequests_CodeHash",
                table: "GuestLoginRequests",
                column: "CodeHash",
                unique: true,
                filter: "[CodeHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GuestLoginRequests_CreatedByUserId",
                table: "GuestLoginRequests",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestLoginRequests_ExpiresAtUtc",
                table: "GuestLoginRequests",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GuestLoginRequests_GuestDeviceId",
                table: "GuestLoginRequests",
                column: "GuestDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestLoginRequests_LegacySessionId",
                table: "GuestLoginRequests",
                column: "LegacySessionId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestLoginRequests_LegacyUserId",
                table: "GuestLoginRequests",
                column: "LegacyUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestLoginRequests_ModifiedByUserId",
                table: "GuestLoginRequests",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestLoginRequests_RequestHash",
                table: "GuestLoginRequests",
                column: "RequestHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestLoginRequests_UserId",
                table: "GuestLoginRequests",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_GuestDevices_GuestDeviceId",
                table: "Sessions",
                column: "GuestDeviceId",
                principalTable: "GuestDevices",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_GuestDevices_GuestDeviceId",
                table: "Sessions");

            migrationBuilder.DropTable(
                name: "GuestLoginRequests");

            migrationBuilder.DropTable(
                name: "GuestDevices");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_GuestDeviceId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "GuestDeviceId",
                table: "Sessions");
        }
    }
}
