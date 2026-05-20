using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class PackageVersionReadmeAndManifestMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfigurationJson",
                table: "PackageVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OverridesJson",
                table: "PackageVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReadmeMarkdown",
                table: "PackageVersions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfigurationJson",
                table: "PackageVersions");

            migrationBuilder.DropColumn(
                name: "OverridesJson",
                table: "PackageVersions");

            migrationBuilder.DropColumn(
                name: "ReadmeMarkdown",
                table: "PackageVersions");
        }
    }
}
