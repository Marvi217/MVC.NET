using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinePlex.Migrations
{
    /// <inheritdoc />
    public partial class AddPublishControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "Screenings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishDate",
                table: "Screenings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishDate",
                table: "Marathons",
                type: "datetime2",
                nullable: true);

            // Existing screenings stay visible after migration
            migrationBuilder.Sql("UPDATE [Screenings] SET [IsPublished] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "Screenings");

            migrationBuilder.DropColumn(
                name: "PublishDate",
                table: "Screenings");

            migrationBuilder.DropColumn(
                name: "PublishDate",
                table: "Marathons");
        }
    }
}
