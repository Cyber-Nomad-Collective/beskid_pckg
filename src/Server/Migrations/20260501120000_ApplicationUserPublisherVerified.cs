using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations;

/// <inheritdoc />
public partial class ApplicationUserPublisherVerified : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsPublisherVerified",
            table: "AspNetUsers",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsPublisherVerified",
            table: "AspNetUsers");
    }
}
