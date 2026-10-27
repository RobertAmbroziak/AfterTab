using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterTab.DAL.EF.Migrations
{
    /// <inheritdoc />
    public partial class ConfirmDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DisplayNameConfirmed",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Preserve deliberately chosen existing names; placeholder guests must complete onboarding.
            migrationBuilder.Sql("UPDATE [Users] SET [DisplayNameConfirmed] = 1 WHERE LEN(LTRIM(RTRIM([DisplayName]))) > 0 AND LTRIM(RTRIM([DisplayName])) <> N'Gość';");

            migrationBuilder.AddColumn<string>(
                name: "ProposedDisplayName",
                table: "GuestLoginRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayNameConfirmed",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProposedDisplayName",
                table: "GuestLoginRequests");
        }
    }
}
