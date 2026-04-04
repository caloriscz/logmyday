using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LogMyDay_EventLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_EventLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_EventLogs_LogMyDay_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "LogMyDay_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_EventLogDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventLogId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_EventLogDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_EventLogDetails_LogMyDay_EventLogs_EventLogId",
                        column: x => x.EventLogId,
                        principalTable: "LogMyDay_EventLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_EventLogDetails_EventLogId",
                table: "LogMyDay_EventLogDetails",
                column: "EventLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_EventLogs_Level",
                table: "LogMyDay_EventLogs",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_EventLogs_UserId_CreatedUtc",
                table: "LogMyDay_EventLogs",
                columns: new[] { "UserId", "CreatedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogMyDay_EventLogDetails");

            migrationBuilder.DropTable(
                name: "LogMyDay_EventLogs");
        }
    }
}
