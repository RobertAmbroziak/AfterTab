using System;
using AfterTab.DAL.EF;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterTab.DAL.EF.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261117130000_McpOAuthConnectedAt")]
public sealed class McpOAuthConnectedAt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ConnectedAtUtc",
            table: "McpOAuthTokens",
            type: "datetimeoffset",
            nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(name: "ConnectedAtUtc", table: "McpOAuthTokens");
}
