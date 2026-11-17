using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTagTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "TriggerDay",
                table: "LogMyDay_Reminders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TriggerId",
                table: "LogMyDay_Reminders",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LogMyDay_TagTriggers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IfTagId = table.Column<int>(type: "INTEGER", nullable: false),
                    IfOperator = table.Column<int>(type: "INTEGER", nullable: false),
                    IfOperand = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PromptTagId = table.Column<int>(type: "INTEGER", nullable: false),
                    Timing = table.Column<int>(type: "INTEGER", nullable: false),
                    OffsetMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    AtTime = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_TagTriggers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TagTriggers_LogMyDay_Tags_IfTagId",
                        column: x => x.IfTagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TagTriggers_LogMyDay_Tags_PromptTagId",
                        column: x => x.PromptTagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Reminders_TriggerId_TriggerDay",
                table: "LogMyDay_Reminders",
                columns: new[] { "TriggerId", "TriggerDay" },
                unique: true,
                filter: "[TriggerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagTriggers_IfTagId",
                table: "LogMyDay_TagTriggers",
                column: "IfTagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagTriggers_PromptTagId",
                table: "LogMyDay_TagTriggers",
                column: "PromptTagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagTriggers_UserId",
                table: "LogMyDay_TagTriggers",
                column: "UserId");

            // NOTE: The Reminder -> TagTrigger FK is intentionally NOT added here. On SQLite, adding
            // a foreign key to an existing table forces a full table rebuild (see
            // database-migrations-guide.md). The relationship is configured in LogMyDayDbContext and
            // trigger delete handles its reminders itself; this migration adds columns and indexes.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogMyDay_TagTriggers");

            migrationBuilder.DropIndex(
                name: "IX_LogMyDay_Reminders_TriggerId_TriggerDay",
                table: "LogMyDay_Reminders");

            migrationBuilder.DropColumn(
                name: "TriggerDay",
                table: "LogMyDay_Reminders");

            migrationBuilder.DropColumn(
                name: "TriggerId",
                table: "LogMyDay_Reminders");
        }
    }
}
