using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinePlex.Migrations
{
    /// <inheritdoc />
    public partial class MergeMarathonReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ScreeningId",
                table: "Reservations",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "PricePaid",
                table: "Reservations",
                type: "decimal(10,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<int>(
                name: "MarathonId",
                table: "Reservations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReservationType",
                table: "Reservations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SeatRow",
                table: "Reservations",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.Sql(@"
                INSERT INTO [Reservations]
                    ([ReservationType], [MarathonId], [SeatRow], [SeatNumber], [AppUserId],
                     [ReservationCode], [PurchaseDate], [Status], [PricePaid], [GuestEmail], [GuestName])
                SELECT 1, [MarathonId], [SeatRow], [SeatNumber], [AppUserId],
                       [ReservationCode], [PurchaseDate], [Status], [PricePaid], [GuestEmail], [GuestName]
                FROM [MarathonReservations]
            ");

            migrationBuilder.DropTable(
                name: "MarathonReservations");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_MarathonId",
                table: "Reservations",
                column: "MarathonId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_Marathons_MarathonId",
                table: "Reservations",
                column: "MarathonId",
                principalTable: "Marathons",
                principalColumn: "MarathonId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_Marathons_MarathonId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_MarathonId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "MarathonId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ReservationType",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "SeatRow",
                table: "Reservations");

            migrationBuilder.AlterColumn<int>(
                name: "ScreeningId",
                table: "Reservations",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PricePaid",
                table: "Reservations",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)");

            migrationBuilder.CreateTable(
                name: "MarathonReservations",
                columns: table => new
                {
                    MarathonReservationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    MarathonId = table.Column<int>(type: "int", nullable: false),
                    GuestEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GuestName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PricePaid = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReservationCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SeatNumber = table.Column<int>(type: "int", nullable: false),
                    SeatRow = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
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
        }
    }
}
