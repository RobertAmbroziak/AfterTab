using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterTab.DAL.EF.Migrations
{
    /// <inheritdoc />
    public partial class BindAccountCookieToBrowser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrowserHash",
                table: "AccountActions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BrowserHash",
                table: "AccountActions");
        }
    }
}
