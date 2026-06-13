using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinePlex.Migrations
{
    /// <inheritdoc />
    public partial class AddHallTypePricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HallTypePricings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HallType = table.Column<int>(type: "int", nullable: false),
                    DefaultPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallTypePricings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HallTypePricings_HallType",
                table: "HallTypePricings",
                column: "HallType",
                unique: true);

            migrationBuilder.InsertData(
                table: "HallTypePricings",
                columns: new[] { "HallType", "DefaultPrice" },
                values: new object[,]
                {
                    { 0, 25.00m },
                    { 1, 32.00m },
                    { 2, 45.00m },
                    { 3, 50.00m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HallTypePricings");
        }
    }
}
