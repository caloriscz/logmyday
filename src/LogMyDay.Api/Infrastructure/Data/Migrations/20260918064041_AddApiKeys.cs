using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LogMyDay_ApiKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Prefix = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    KeyHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevokedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastUsedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_ApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_ApiKeys_LogMyDay_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "LogMyDay_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_ApiKeys_Prefix",
                table: "LogMyDay_ApiKeys",
                column: "Prefix");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_ApiKeys_UserId",
                table: "LogMyDay_ApiKeys",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogMyDay_ApiKeys");
        }
    }
}
