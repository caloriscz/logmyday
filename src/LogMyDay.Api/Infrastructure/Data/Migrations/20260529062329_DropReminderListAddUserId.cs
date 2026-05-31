using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropReminderListAddUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reminders move from list-scoped to user-scoped. Add UserId first, copy from
            // the owning ReminderList, then drop the FK/index/column/table.

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "LogMyDay_Reminders",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(@"
                UPDATE LogMyDay_Reminders
                SET UserId = (
                    SELECT UserId FROM LogMyDay_ReminderLists
                    WHERE LogMyDay_ReminderLists.Id = LogMyDay_Reminders.ReminderListId
                );
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_LogMyDay_Reminders_LogMyDay_ReminderLists_ReminderListId",
                table: "LogMyDay_Reminders");

            migrationBuilder.DropIndex(
                name: "IX_LogMyDay_Reminders_ReminderListId",
                table: "LogMyDay_Reminders");

            migrationBuilder.DropColumn(
                name: "ReminderListId",
                table: "LogMyDay_Reminders");

            migrationBuilder.DropTable(
                name: "LogMyDay_ReminderLists");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Reminders_UserId",
                table: "LogMyDay_Reminders",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LogMyDay_Reminders_UserId",
                table: "LogMyDay_Reminders");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "LogMyDay_Reminders");

            migrationBuilder.AddColumn<int>(
                name: "ReminderListId",
                table: "LogMyDay_Reminders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "LogMyDay_ReminderLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ShowOnHomepage = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_ReminderLists", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Reminders_ReminderListId",
                table: "LogMyDay_Reminders",
                column: "ReminderListId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_ReminderLists_UserId",
                table: "LogMyDay_ReminderLists",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_LogMyDay_Reminders_LogMyDay_ReminderLists_ReminderListId",
                table: "LogMyDay_Reminders",
                column: "ReminderListId",
                principalTable: "LogMyDay_ReminderLists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
