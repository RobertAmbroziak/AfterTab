using AfterTab.DAL.EF;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AfterTab.DAL.Migrations.PostgreSql.Migrations;

public sealed partial class AddMcpUserInstructions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<string>(
            name: "McpInstructions",
            table: "Users",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: false,
            defaultValue: "");

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(name: "McpInstructions", table: "Users");
}
