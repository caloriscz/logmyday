using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTagGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LogMyDay_TagGroups",
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
                    table.PrimaryKey("PK_LogMyDay_TagGroups", x => x.Id);
                });

            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "LogMyDay_Tags",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Tags_GroupId",
                table: "LogMyDay_Tags",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagGroups_UserId",
                table: "LogMyDay_TagGroups",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogMyDay_TagGroups");

            migrationBuilder.DropIndex(
                name: "IX_LogMyDay_Tags_GroupId",
                table: "LogMyDay_Tags");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "LogMyDay_Tags");
        }
    }
}