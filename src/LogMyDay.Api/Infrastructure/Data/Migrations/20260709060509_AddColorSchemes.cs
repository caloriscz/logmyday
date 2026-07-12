using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddColorSchemes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ColorSchemeId",
                table: "LogMyDay_Tags",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LogMyDay_ColorSchemes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_ColorSchemes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_ColorSchemeEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ColorSchemeId = table.Column<int>(type: "INTEGER", nullable: false),
                    RangeFrom = table.Column<double>(type: "REAL", nullable: true),
                    RangeTo = table.Column<double>(type: "REAL", nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_ColorSchemeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_ColorSchemeEntries_LogMyDay_ColorSchemes_ColorSchemeId",
                        column: x => x.ColorSchemeId,
                        principalTable: "LogMyDay_ColorSchemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Tags_ColorSchemeId",
                table: "LogMyDay_Tags",
                column: "ColorSchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_ColorSchemeEntries_ColorSchemeId",
                table: "LogMyDay_ColorSchemeEntries",
                column: "ColorSchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_ColorSchemes_UserId",
                table: "LogMyDay_ColorSchemes",
                column: "UserId");

            // NOTE: The Tag -> ColorScheme FK is intentionally NOT added here. On SQLite, adding a
            // foreign key to an existing table forces a full table rebuild (see
            // database-migrations-guide.md). The relationship + SetNull behavior is configured in
            // LogMyDayDbContext; this migration only adds the column and its index.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogMyDay_ColorSchemeEntries");

            migrationBuilder.DropTable(
                name: "LogMyDay_ColorSchemes");

            migrationBuilder.DropIndex(
                name: "IX_LogMyDay_Tags_ColorSchemeId",
                table: "LogMyDay_Tags");

            migrationBuilder.DropColumn(
                name: "ColorSchemeId",
                table: "LogMyDay_Tags");
        }
    }
}
