using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTagDayLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Provider-neutral migration — explicit column types removed so EF infers them
            // per-provider (INT vs INTEGER, NVARCHAR vs TEXT, UNIQUEIDENTIFIER vs TEXT, BIT vs
            // INTEGER, DATETIME2 vs TEXT, DATE vs TEXT). Keeps one set of migrations usable
            // against both SQLite (dev) and SQL Server (preproduction/prod).

            migrationBuilder.CreateTable(
                name: "LogMyDay_TagDayLocks",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(nullable: false),
                    TagId = table.Column<int>(nullable: false),
                    Date = table.Column<DateOnly>(nullable: false),
                    IsLocked = table.Column<bool>(nullable: false),
                    SetAt = table.Column<DateTime>(nullable: false),
                    SetBy = table.Column<int>(nullable: false),
                    Reason = table.Column<string>(maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_TagDayLocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TagDayLocks_LogMyDay_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagDayLocks_TagId",
                table: "LogMyDay_TagDayLocks",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagDayLocks_UserId_TagId_Date",
                table: "LogMyDay_TagDayLocks",
                columns: new[] { "UserId", "TagId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogMyDay_TagDayLocks");
        }
    }
}
