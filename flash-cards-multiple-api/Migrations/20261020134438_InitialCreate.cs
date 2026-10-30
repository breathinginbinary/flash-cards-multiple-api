using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace flash_cards_multiple_api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardSeries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Topic = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardSeries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CardSeriesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Front = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Back = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cards", x => x.Id);
                    table.CheckConstraint("CK_Cards_Position", "\"Position\" >= 0 AND \"Position\" < 3");
                    table.ForeignKey(
                        name: "FK_Cards_CardSeries_CardSeriesId",
                        column: x => x.CardSeriesId,
                        principalTable: "CardSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "CardSeries",
                columns: new[] { "Id", "ConcurrencyStamp", "CreatedAt", "ImageUrl", "Topic", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("a1b2c3d4-0001-4000-8000-000000000001"), "a1b2c3d4000140008000000000000001", new DateTime(2026, 10, 20, 0, 0, 0, 0, DateTimeKind.Utc), "/images/Dog.png", "Dog", new DateTime(2026, 10, 20, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1b2c3d4-0002-4000-8000-000000000002"), "a1b2c3d4000240008000000000000002", new DateTime(2026, 10, 20, 0, 0, 0, 0, DateTimeKind.Utc), "/images/Cat.png", "Cat", new DateTime(2026, 10, 20, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "Cards",
                columns: new[] { "Id", "Back", "CardSeriesId", "Front", "Position" },
                values: new object[,]
                {
                    { new Guid("b1b2c3d4-0001-4000-8000-000000000001"), "Hund", new Guid("a1b2c3d4-0001-4000-8000-000000000001"), "German", 0 },
                    { new Guid("b1b2c3d4-0001-4000-8000-000000000002"), "perro", new Guid("a1b2c3d4-0001-4000-8000-000000000001"), "Spanish", 1 },
                    { new Guid("b1b2c3d4-0002-4000-8000-000000000001"), "Katze", new Guid("a1b2c3d4-0002-4000-8000-000000000002"), "German", 0 },
                    { new Guid("b1b2c3d4-0002-4000-8000-000000000002"), "gato", new Guid("a1b2c3d4-0002-4000-8000-000000000002"), "Spanish", 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_CardSeriesId_Position",
                table: "Cards",
                columns: new[] { "CardSeriesId", "Position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cards");

            migrationBuilder.DropTable(
                name: "CardSeries");
        }
    }
}
