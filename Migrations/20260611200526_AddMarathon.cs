using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinePlex.Migrations
{
    /// <inheritdoc />
    public partial class AddMarathon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Marathons",
                columns: table => new
                {
                    MarathonId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PosterUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    HallId = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Marathons", x => x.MarathonId);
                    table.ForeignKey(
                        name: "FK_Marathons_Halls_HallId",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "HallId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarathonReservations",
                columns: table => new
                {
                    MarathonReservationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MarathonId = table.Column<int>(type: "int", nullable: false),
                    AppUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    SeatRow = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SeatNumber = table.Column<int>(type: "int", nullable: false),
                    ReservationCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PricePaid = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    GuestEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GuestName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarathonReservations", x => x.MarathonReservationId);
                    table.ForeignKey(
                        name: "FK_MarathonReservations_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MarathonReservations_Marathons_MarathonId",
                        column: x => x.MarathonId,
                        principalTable: "Marathons",
                        principalColumn: "MarathonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<int>(
                name: "MarathonId",
                table: "Screenings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarathonReservations_AppUserId",
                table: "MarathonReservations",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MarathonReservations_MarathonId",
                table: "MarathonReservations",
                column: "MarathonId");

            migrationBuilder.CreateIndex(
                name: "IX_MarathonReservations_ReservationCode",
                table: "MarathonReservations",
                column: "ReservationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Marathons_HallId",
                table: "Marathons",
                column: "HallId");

            migrationBuilder.CreateIndex(
                name: "IX_Screenings_MarathonId",
                table: "Screenings",
                column: "MarathonId");

            migrationBuilder.AddForeignKey(
                name: "FK_Screenings_Marathons_MarathonId",
                table: "Screenings",
                column: "MarathonId",
                principalTable: "Marathons",
                principalColumn: "MarathonId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Screenings_Marathons_MarathonId",
                table: "Screenings");

            migrationBuilder.DropIndex(
                name: "IX_Screenings_MarathonId",
                table: "Screenings");

            migrationBuilder.DropColumn(
                name: "MarathonId",
                table: "Screenings");

            migrationBuilder.DropTable(
                name: "MarathonReservations");

            migrationBuilder.DropTable(
                name: "Marathons");
        }
    }
}
