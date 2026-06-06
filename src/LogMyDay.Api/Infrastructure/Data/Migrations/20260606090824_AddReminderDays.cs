using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LogMyDay_ReminderDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReminderId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    IsDone = table.Column<bool>(type: "INTEGER", nullable: false),
                    DoneAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsSkipped = table.Column<bool>(type: "INTEGER", nullable: false),
                    SkippedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletionValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_ReminderDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_ReminderDays_LogMyDay_Reminders_ReminderId",
                        column: x => x.ReminderId,
                        principalTable: "LogMyDay_Reminders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_ReminderDays_ReminderId_Date",
                table: "LogMyDay_ReminderDays",
                columns: new[] { "ReminderId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_ReminderDays_UserId_Date",
                table: "LogMyDay_ReminderDays",
                columns: new[] { "UserId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogMyDay_ReminderDays");
        }
    }
}
