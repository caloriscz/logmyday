using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TodoItem_RecurrenceAndMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutoLogMode",
                table: "LogMyDay_TodoItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

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

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceType",
                table: "LogMyDay_TodoItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoLogMode",
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

            migrationBuilder.DropColumn(
                name: "RecurrenceType",
                table: "LogMyDay_TodoItems");
        }
    }
}
