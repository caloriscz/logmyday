using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TodoItem_FixSqlServerDateTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<TimeSpan>(
                name: "NudgeInterval",
                table: "LogMyDay_Notifications",
                type: "TEXT",
                nullable: true,
                defaultValue: new TimeSpan(0, 0, 15, 0, 0),
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true,
                oldDefaultValue: "00:15:00");

            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.AlterColumn<DateOnly>(
                    name: "StartDate",
                    table: "LogMyDay_TodoItems",
                    type: "date",
                    nullable: true,
                    oldClrType: typeof(DateOnly),
                    oldType: "TEXT",
                    oldNullable: true);

                migrationBuilder.AlterColumn<DateOnly>(
                    name: "DueDate",
                    table: "LogMyDay_TodoItems",
                    type: "date",
                    nullable: true,
                    oldClrType: typeof(DateOnly),
                    oldType: "TEXT",
                    oldNullable: true);

                migrationBuilder.AlterColumn<TimeOnly>(
                    name: "NotifyAt",
                    table: "LogMyDay_TodoItems",
                    type: "time",
                    nullable: true,
                    oldClrType: typeof(TimeOnly),
                    oldType: "TEXT",
                    oldNullable: true);

                migrationBuilder.AlterColumn<DateOnly>(
                    name: "MonitorFromDate",
                    table: "LogMyDay_TodoItems",
                    type: "date",
                    nullable: true,
                    oldClrType: typeof(DateOnly),
                    oldType: "TEXT",
                    oldNullable: true);

                migrationBuilder.AlterColumn<DateOnly>(
                    name: "MonitorToDate",
                    table: "LogMyDay_TodoItems",
                    type: "date",
                    nullable: true,
                    oldClrType: typeof(DateOnly),
                    oldType: "TEXT",
                    oldNullable: true);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NudgeInterval",
                table: "LogMyDay_Notifications",
                type: "TEXT",
                nullable: true,
                defaultValue: "00:15:00",
                oldClrType: typeof(TimeSpan),
                oldType: "TEXT",
                oldNullable: true,
                oldDefaultValue: new TimeSpan(0, 0, 15, 0, 0));

            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.AlterColumn<DateOnly>(
                    name: "StartDate",
                    table: "LogMyDay_TodoItems",
                    type: "TEXT",
                    nullable: true,
                    oldClrType: typeof(DateOnly),
                    oldType: "date",
                    oldNullable: true);

                migrationBuilder.AlterColumn<DateOnly>(
                    name: "DueDate",
                    table: "LogMyDay_TodoItems",
                    type: "TEXT",
                    nullable: true,
                    oldClrType: typeof(DateOnly),
                    oldType: "date",
                    oldNullable: true);

                migrationBuilder.AlterColumn<TimeOnly>(
                    name: "NotifyAt",
                    table: "LogMyDay_TodoItems",
                    type: "TEXT",
                    nullable: true,
                    oldClrType: typeof(TimeOnly),
                    oldType: "time",
                    oldNullable: true);

                migrationBuilder.AlterColumn<DateOnly>(
                    name: "MonitorFromDate",
                    table: "LogMyDay_TodoItems",
                    type: "TEXT",
                    nullable: true,
                    oldClrType: typeof(DateOnly),
                    oldType: "date",
                    oldNullable: true);

                migrationBuilder.AlterColumn<DateOnly>(
                    name: "MonitorToDate",
                    table: "LogMyDay_TodoItems",
                    type: "TEXT",
                    nullable: true,
                    oldClrType: typeof(DateOnly),
                    oldType: "TEXT",
                    oldNullable: true);
            }
        }
    }
}
