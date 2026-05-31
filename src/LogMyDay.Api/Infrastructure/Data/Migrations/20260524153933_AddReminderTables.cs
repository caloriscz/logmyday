using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Provider-neutral — see AddTagDayLock for rationale.

            migrationBuilder.CreateTable(
                name: "LogMyDay_ReminderLists",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(nullable: false),
                    Name = table.Column<string>(maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(nullable: false, defaultValue: 0),
                    ShowOnHomepage = table.Column<bool>(nullable: false),
                    DateCreated = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_ReminderLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_Reminders",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReminderListId = table.Column<int>(nullable: false),
                    Title = table.Column<string>(maxLength: 500, nullable: false),
                    Notes = table.Column<string>(nullable: true),
                    NotifyAt = table.Column<TimeOnly>(nullable: true),
                    IsDone = table.Column<bool>(nullable: false, defaultValue: false),
                    DoneAt = table.Column<DateTime>(nullable: true),
                    SkippedAt = table.Column<DateTime>(nullable: true),
                    DisplayOrder = table.Column<int>(nullable: false, defaultValue: 0),
                    DateCreated = table.Column<DateTime>(nullable: false),
                    RecurrenceType = table.Column<int>(nullable: false),
                    AutoLogMode = table.Column<int>(nullable: false),
                    CompletionTagId = table.Column<int>(nullable: true),
                    MonitorDaysBack = table.Column<int>(nullable: true),
                    MonitorFromDate = table.Column<DateOnly>(nullable: true),
                    MonitorToDate = table.Column<DateOnly>(nullable: true),
                    AllowUnfilled = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_Reminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_Reminders_LogMyDay_ReminderLists_ReminderListId",
                        column: x => x.ReminderListId,
                        principalTable: "LogMyDay_ReminderLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LogMyDay_Reminders_LogMyDay_Tags_CompletionTagId",
                        column: x => x.CompletionTagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_ReminderLists_UserId",
                table: "LogMyDay_ReminderLists",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Reminders_CompletionTagId",
                table: "LogMyDay_Reminders",
                column: "CompletionTagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Reminders_ReminderListId",
                table: "LogMyDay_Reminders",
                column: "ReminderListId");

            // NOTE: This migration creates the new Reminder/ReminderList tables only.
            // The follow-up migration `MoveRemindersAndDropOldFields` copies the Reminder-typed
            // rows from LogMyDay_TodoLists/LogMyDay_TodoItems and deletes them from the old
            // tables (provider-aware with IDENTITY_INSERT wrapping for SQL Server).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogMyDay_Reminders");

            migrationBuilder.DropTable(
                name: "LogMyDay_ReminderLists");
        }
    }
}
