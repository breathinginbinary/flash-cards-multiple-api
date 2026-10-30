using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace flash_cards_multiple_api.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersAndOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "CardSeries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "CardSeries",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0001-4000-8000-000000000001"),
                column: "UserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "CardSeries",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0002-4000-8000-000000000002"),
                column: "UserId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_CardSeries_UserId",
                table: "CardSeries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CardSeries_Users_UserId",
                table: "CardSeries",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CardSeries_Users_UserId",
                table: "CardSeries");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_CardSeries_UserId",
                table: "CardSeries");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "CardSeries");
        }
    }
}
