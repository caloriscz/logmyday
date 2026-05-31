using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveRemindersAndDropOldFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Big-bang data migration: copy Reminder-typed rows from the old TodoList/TodoItem
            // tables into the new Reminder/ReminderList tables, preserving Ids so existing
            // Android AlarmManager PendingIntent request codes (keyed by TodoItem.Id) continue
            // to match. Then delete the moved rows from the old tables BEFORE the ListType column
            // is dropped — otherwise the WHERE filter wouldn't have a column to read.
            //
            // SQL Server: identity columns require `SET IDENTITY_INSERT ... ON` to insert explicit Ids.
            // SQLite: autoincrement Id is writable by default; no wrapper needed.

            var isSqlServer = migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer";

            if (isSqlServer)
            {
                migrationBuilder.Sql("SET IDENTITY_INSERT LogMyDay_ReminderLists ON;");
            }

            migrationBuilder.Sql(@"
                INSERT INTO LogMyDay_ReminderLists (Id, UserId, Name, DisplayOrder, ShowOnHomepage, DateCreated)
                SELECT Id, UserId, Name, DisplayOrder, ShowOnHomepage, DateCreated
                FROM LogMyDay_TodoLists
                WHERE ListType = 1;
            ");

            if (isSqlServer)
            {
                migrationBuilder.Sql("SET IDENTITY_INSERT LogMyDay_ReminderLists OFF;");
                migrationBuilder.Sql("SET IDENTITY_INSERT LogMyDay_Reminders ON;");
            }

            migrationBuilder.Sql(@"
                INSERT INTO LogMyDay_Reminders (
                    Id, ReminderListId, Title, Notes, NotifyAt, IsDone, DoneAt, SkippedAt,
                    DisplayOrder, DateCreated, RecurrenceType, AutoLogMode, CompletionTagId,
                    MonitorDaysBack, MonitorFromDate, MonitorToDate, AllowUnfilled)
                SELECT
                    i.Id, i.ListId, i.Title, i.Notes, i.NotifyAt, i.IsDone, i.DoneAt, i.SkippedAt,
                    i.DisplayOrder, i.DateCreated, i.RecurrenceType, i.AutoLogMode, i.CompletionTagId,
                    i.MonitorDaysBack, i.MonitorFromDate, i.MonitorToDate, i.AllowUnfilled
                FROM LogMyDay_TodoItems i
                INNER JOIN LogMyDay_TodoLists l ON i.ListId = l.Id
                WHERE l.ListType = 1;
            ");

            if (isSqlServer)
            {
                migrationBuilder.Sql("SET IDENTITY_INSERT LogMyDay_Reminders OFF;");
            }

            migrationBuilder.Sql(@"
                DELETE FROM LogMyDay_TodoItems
                WHERE ListId IN (SELECT Id FROM LogMyDay_TodoLists WHERE ListType = 1);
            ");

            migrationBuilder.Sql("DELETE FROM LogMyDay_TodoLists WHERE ListType = 1;");

            migrationBuilder.DropColumn(
                name: "ListType",
                table: "LogMyDay_TodoLists");

            migrationBuilder.DropColumn(
                name: "AllowUnfilled",
                table: "LogMyDay_TodoItems");

            migrationBuilder.DropColumn(
                name: "MonitorDaysBack",
                table: "LogMyDay_TodoItems");

            migrationBuilder.DropColumn(
                name: "MonitorFromDate",
                table: "LogMyDay_TodoItems");

            migrationBuilder.DropColumn(
                name: "MonitorToDate",
                table: "LogMyDay_TodoItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ListType",
                table: "LogMyDay_TodoLists",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "AllowUnfilled",
                table: "LogMyDay_TodoItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MonitorDaysBack",
                table: "LogMyDay_TodoItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MonitorFromDate",
                table: "LogMyDay_TodoItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MonitorToDate",
                table: "LogMyDay_TodoItems",
                type: "TEXT",
                nullable: true);
        }
    }
}
